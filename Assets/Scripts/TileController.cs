﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileController : MonoBehaviour
{
    private GameFlowManager game;


    // Tambahkan fungsi u/ mengubah tile pada TileController, dimana akan memakai ID u/ membedakan tipe2 antar tile dg ID sbg angka pada list TileTypes

    public int id;

    private BoardManager board;
    private SpriteRenderer render;

    private static readonly Color selectedColor = new Color(0.5f, 0.5f, 0.5f);
    private static readonly Color normalColor = Color.white;

    private static TileController previousSelected = null;
    private bool isSelected = false;


    private static readonly float moveDuration = 0.5f;

    private static readonly float destroyBigDuration = 0.1f;
    private static readonly float destroySmallDuration = 0.4f;

    private static readonly Vector2 sizeBig = Vector2.one * 1.2f;
    private static readonly Vector2 sizeSmall = Vector2.zero;
    private static readonly Vector2 sizeNormal = Vector2.one;

    public bool IsDestroyed { get; private set; }

    public IEnumerator SetDestroyed(System.Action onCompleted){
        IsDestroyed = true;
        id = -1;
        name = "TILE_NULL";

        Vector2 startSize = transform.localScale;
        float time = 0.0f;

        while (time < destroyBigDuration)
        {
            transform.localScale = Vector2.Lerp(startSize, sizeBig, time / destroyBigDuration);
            time += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        transform.localScale = sizeBig;

        startSize = transform.localScale;
        time = 0.0f;

        while (time < destroySmallDuration)
        {
            transform.localScale = Vector2.Lerp(startSize, sizeSmall, time / destroySmallDuration);
            time += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        transform.localScale = sizeSmall;

        render.sprite = null;

        onCompleted?.Invoke();
    }

    private void Awake() {
        board = BoardManager.Instance;
        render = GetComponent<SpriteRenderer>();

        game = GameFlowManager.Instance;
    }

    public void ChangeId(int id, int x, int y)
    {
        render.sprite = board.tileTypes[id];
        this.id = id;

        name = "TILE_" + id + "(" + x + "," + y + ")";
    }



    private static readonly Vector2[] adjacentDirection = new Vector2[] {Vector2.up, Vector2.down, Vector2.left, Vector2.right};

    #region Adjacent

    private TileController GetAdjacent(Vector2 castDir){
        RaycastHit2D hit = Physics2D.Raycast(transform.position, castDir, render.size.x);

        if (hit)
        {
            return hit.collider.GetComponent<TileController>();
        }

        return null;
    }

    public List<TileController> GetAllAdjacentTiles(){
        List<TileController> adjacentTiles = new List<TileController>();

        for (int i = 0; i < adjacentDirection.Length; i++)
        {
            adjacentTiles.Add(GetAdjacent(adjacentDirection[i]));
        }

        return adjacentTiles;
    }

    #endregion





    #region Check Match

    private List<TileController> GetMatch(Vector2 castDir){
        List<TileController> matchingTiles = new List<TileController>();
        RaycastHit2D hit = Physics2D.Raycast(transform.position, castDir, render.size.x);

        while (hit)
        {
            TileController otherTile = hit.collider.GetComponent<TileController>();
            if (otherTile.id != id || otherTile.IsDestroyed)
            {
                break;
            }

            matchingTiles.Add(otherTile);
            hit = Physics2D.Raycast(otherTile.transform.position, castDir, render.size.x);
        }

        return matchingTiles;
    }

    private List<TileController> GetOneLineMatch(Vector2[] paths){
        List<TileController> matchingTiles = new List<TileController>();

        for (int i = 0; i < paths.Length; i++)
        {
            matchingTiles.AddRange(GetMatch(paths[i]));
        }

        // Hanya match jika > 2 (3 with itself) dalam 1 baris
        if (matchingTiles.Count >= 2)
        {
            return matchingTiles;
        }

        return null;
    }

    public List<TileController> GetAllMatches(){
        if (IsDestroyed)
        {
            return null;
        }

        List<TileController> matchingTiles = new List<TileController>();

        // Dapatkan matches untuk baris horizontal dan vertical
        List<TileController> horizontalMatchingTiles = GetOneLineMatch(new Vector2[2] {Vector2.up, Vector2.down});
        List<TileController> verticalMatchingTiles = GetOneLineMatch(new Vector2[2] {Vector2.left, Vector2.right});

        if (horizontalMatchingTiles != null)
        {
            matchingTiles.AddRange(horizontalMatchingTiles);
        }

        if (verticalMatchingTiles != null)
        {
            matchingTiles.AddRange(verticalMatchingTiles);
        }

        // Tambahkan itself to matched jika match ditemukan
        if (matchingTiles != null && matchingTiles.Count >= 2)
        {
            matchingTiles.Add(this);
        }

        return matchingTiles;
    }

    #endregion



    private void OnMouseDown() {
        // Non-selectable condition
        if (render.sprite == null || board.IsAnimating || game.IsGameOver)
        {
            return;
        }

        SoundManager.Instance.PlayTap();

        // Already selected tile?
        if (isSelected)
        {
            Deselect();
        }
        else
        {
            // If nothing selected yet
            if (previousSelected == null)
            {
                Select();
            }
            else
            {
                // Is it an adjacent tile?
                if (GetAllAdjacentTiles().Contains(previousSelected))
                {
                    TileController otherTile = previousSelected;
                    previousSelected.Deselect();

                    // Swap Tile
                    SwapTile(otherTile, () => {
                        if (board.GetAllMatches().Count > 0)
                        {
                            board.Process();
                        }
                        else
                        {
                            SoundManager.Instance.PlayWrong();
                            SwapTile(otherTile);
                        }
                    });
                }

                // If not adjacent, then change selected
                else
                {
                    previousSelected.Deselect();
                    Select();
                }
            }
        }
    }

    public void SwapTile(TileController otherTile, System.Action onCompleted = null)
    {
        StartCoroutine(board.SwapTilePosition(this, otherTile, onCompleted));
    }

    public IEnumerator MoveTilePosition(Vector2 targetPosition, System.Action onCompleted)
    {
        Vector2 startPosition = transform.position;
        float time = 0.0f;

        // Run animation pada frame selanjutnya untuk alasan keamanan
        // Jalankan pada frame berikutnya agar kalkulasi tidak terganggu
        yield return new WaitForEndOfFrame();

        while (time < moveDuration)
        {
            // Pindahkan posisi sesuai garis linier antara starPosition dg targetPosition dengan menambahkan waktu dengan waktu proses frame tsb
            transform.position = Vector2.Lerp(startPosition, targetPosition, time/moveDuration);
            time += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        transform.position = targetPosition;

        onCompleted?.Invoke();
    }

    public void GenerateRandomTile(int x, int y){
        transform.localScale = sizeNormal;
        IsDestroyed = false;

        ChangeId(Random.Range(0, board.tileTypes.Count), x, y);
    }




    #region Select & Deselect

    private void Select(){
        isSelected = true;
        render.color = selectedColor;
        previousSelected = this;
    }

    private void Deselect(){
        isSelected = false;
        render.color = normalColor;
        previousSelected = null;
    }

    #endregion



    // Start is called before the first frame update
    void Start()
    {
        IsDestroyed = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}